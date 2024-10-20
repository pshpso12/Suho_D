const express = require('express');
const cors = require('cors');
const { Sequelize, DataTypes } = require('sequelize');

const app = express();
const PORT = xxxx;

const DB_NAME = 'xxxx';
const DB_USER = 'xxxx';
const DB_PASSWORD = 'xxxx';
const DB_HOST = 'xxxx';

const sequelize = new Sequelize(DB_NAME, DB_USER, DB_PASSWORD, {
  host: DB_HOST,
  dialect: 'mysql',
  logging: false,
});

const User = require('./sequelize/user_model')(sequelize, DataTypes);
const Outfit = require('./sequelize/outfit_model')(sequelize, DataTypes);
const Accessory = require('./sequelize/accessory_model')(sequelize, DataTypes);
const Character = require('./sequelize/character_model')(sequelize, DataTypes);
const LootItem = require('./sequelize/lootitem_model')(sequelize, DataTypes);

User.hasMany(Outfit, { foreignKey: 'UserID' });
User.hasMany(Character, { foreignKey: 'UserID' });
User.hasMany(Accessory, { foreignKey: 'UserID' });
User.hasMany(LootItem, { foreignKey: 'UserID' });
app.use(cors());
app.use(express.json());

app.get('/', (req, res) => {
  res.send('Hello from Node.js server!');
});

app.get('/db-check', async (req, res) => {
  try {
    await sequelize.authenticate();
    res.send('Database connection established successfully.');
  } catch (error) {
    res.send('Error connecting to the database: ' + error.message);
  }
});

app.post('/check-steamID', async (req, res) => {
  const { steamID } = req.body;

  try {
    const user = await User.findOne({ where: { SteamID: steamID } });

    if (user) {
      res.json({ 
        exists: true, 
        message: "SteamID already exists!", 
        userID: user.UserID,
        isban: user.isBan
      });
    } else {
      res.json({ exists: false, message: "SteamID not found!" });
    }

  } catch (error) {
      console.log(error);
      res.status(500).json({ success: false, message: "Error checking SteamID: " + error.message });
  }
});

app.post('/get-userdetails', async (req, res) => {
  const { steamID } = req.body;

  try {
    const user = await User.findOne({ where: { SteamID: steamID } });

    if (user) {
      res.json({
        exists: true,
        userID : user.UserID,
        Nickname: user.Nickname,
        Level: user.Level,
        ExperiencePoints: user.ExperiencePoints,
        MainCharacterID: user.MainCharacterID,
        cashpoint: user.cashpoint,
        basepoint: user.basepoint
      });
    } else {
      res.json({ exists: false});
    }

  } catch (error) {
    console.log(error);
    res.status(500).json({ success: false, message: "Error fetching user details: " + error.message });
  }
});

app.post('/get-characters', async (req, res) => {
  const { userID } = req.body;

  try {
    const characters = await Character.findAll({ where: { UserID: userID } });

    res.json({ characters: characters });
  } catch (error) {
    console.log(error);
    res.status(500).json({ success: false, message: "Error fetching characters: " + error.message });
  }
});

app.post('/get-outfit', async (req, res) => {
  const { userID } = req.body;

  try {
    const outfits = await Outfit.findAll({ where: { UserID: userID } });

    res.json({ outfits: outfits });
  } catch (error) {
    console.log(error);
    res.status(500).json({ success: false, message: "Error fetching outfits: " + error.message });
  }
});

app.post('/get-accessory', async (req, res) => {
  const { userID } = req.body;

  try {
    const accessories = await Accessory.findAll({ where: { UserID: userID } });

    res.json({ accessories: accessories });
  } catch (error) {
    console.log(error);
    res.status(500).json({ success: false, message: "Error fetching accessories: " + error.message });
  }
});

app.post('/check-nickname', async (req, res) => {
  const { nickname } = req.body;

  try {
    const user = await User.findOne({ where: { Nickname: nickname } });

    if (user) {
      res.json({ 
        exists: true, 
        message: "Nickname already exists!" 
      });
    } else {
      res.json({ exists: false, message: "Nickname available!" });
    }

  } catch (error) {
      console.log(error);
      res.status(500).json({ success: false, message: "Error checking nickname: " + error.message });
  }
});

app.post('/add-user', async (req, res) => {
  const { steamID, nickname } = req.body;

  try {
    const existingUser = await User.findOne({ where: { SteamID: steamID } });

    if (existingUser) {
      res.status(400).json({ 
        success: false, 
        message: "SteamID already exists!" 
      });
      return;
    }
    /*초기 메인캐릭터는 10개 중 랜덤한 하나*/
    const randomMainCharacterID = Math.floor(Math.random() * 10) + 1;
    
    const newUser = await User.create({
      SteamID: steamID,
      Nickname: nickname,
      MainCharacterID: randomMainCharacterID,
      isBan: false
    });

    if (newUser) {
      let outfits = [];
      /*의상을 Null값이 불가능하므로 기본 의상을 일괄 등록*/
      for (let i = 1; i <= 10; i++) {
        outfits.push({ Description: `N${i}_TOP_base`, Type: 'Top', UserID: newUser.UserID, Character_costume: i });
        outfits.push({ Description: `N${i}_BOTTOM_base`, Type: 'Bottom', UserID: newUser.UserID, Character_costume: i });
        outfits.push({ Description: `N${i}_SHOES_base`, Type: 'Shoes', UserID: newUser.UserID, Character_costume: i });
      }
      await Outfit.bulkCreate(outfits);
      

      for (let i = 1; i <= 10; i++) {
        const topOutfit = await Outfit.findOne({
            where: {
                Description: `N${i}_TOP_base`,
                UserID: newUser.UserID
            }
        });
    
        const bottomOutfit = await Outfit.findOne({
            where: {
                Description: `N${i}_BOTTOM_base`,
                UserID: newUser.UserID
            }
        });
    
        const shoesOutfit = await Outfit.findOne({
            where: {
                Description: `N${i}_SHOES_base`,
                UserID: newUser.UserID
            }
        });
        /*10개의 캐릭터에 기본 의상 적용*/
        await Character.create({
            UserID: newUser.UserID,
            CharacterType: i,
            TopOutfitID: topOutfit.OutfitID,
            BottomOutfitID: bottomOutfit.OutfitID,
            ShoesOutfitID: shoesOutfit.OutfitID
        });
    }

      res.json({ 
        success: true,
        message: "User, outfits, and characters added successfully!"
      });
    } else {
      res.status(500).json({
        success: false,
        message: "Failed to add user!"
      });
    }

  } catch (error) {
    console.log(error);
    res.status(500).json({ 
      success: false, 
      message: "Error adding user: " + error.message 
    });
  }
});

app.post('/update-main-character', async (req, res) => {
  const { userID, newMainCharacterID } = req.body;

  try {
    const user = await User.findOne({ where: { UserID: userID } });

    if (user) {
      await user.update({ MainCharacterID: newMainCharacterID });
      res.json({ 
        success: true, 
        message: "MainCharacterID updated successfully!" 
      });
    } else {
      res.status(404).json({ 
        success: false, 
        message: "User not found!" 
      });
    }

  } catch (error) {
    console.log(error);
    res.status(500).json({ 
      success: false, 
      message: "Error updating MainCharacterID: " + error.message 
    });
  }
});

app.post('/update-character-clothes', async (req, res) => {
  const { userID, characterNum, type, outfitID } = req.body;

  try {
    const outfit = await Outfit.findOne({ where: { OutfitID: outfitID, UserID: userID } });
    if (!outfit) {
      return res.status(400).json({ success: false, message: "Invalid outfit or not owned by the user" });
    }

    const character = await Character.findOne({ where: { UserID: userID, CharacterType: characterNum } });
    if (!character) {
      return res.status(404).json({ success: false, message: "Character not found" });
    }

    switch (type) {
      case 'Top':
        await character.update({ TopOutfitID: outfitID });
        break;
      case 'Bottom':
        await character.update({ BottomOutfitID: outfitID });
        break;
      case 'Shoes':
        await character.update({ ShoesOutfitID: outfitID });
        break;
      default:
        return res.status(400).json({ success: false, message: "Invalid outfit type" });
    }

    res.json({ success: true, message: "Character outfit updated successfully!" });
  } catch (error) {
    console.log(error);
    res.status(500).json({ success: false, message: "Error updating character outfit: " + error.message });
  }
});

app.post('/check-item-existence', async (req, res) => {
  const { userID, characterNum, type, description, priceType, price, worn } = req.body;

  try {
      const existingItem = await Outfit.findOne({
          where: {
              UserID: userID,
              Character_costume: characterNum,
              Type: type,
              Description: description
          }
      });

      const user = await User.findOne({ where: { UserID: userID } });
      if (!user) {
          return res.status(404).json({ success: false, message: "User not found" });
      }

      if (existingItem) {
          return res.json({ success: true, alreadyOwned: true, cashpoint: user.cashpoint, basepoint: user.basepoint });
      }

      if (priceType === "CP" && user.cashpoint < price) {
          return res.json({ success: true, sufficientFunds: false, cashpoint: user.cashpoint, basepoint: user.basepoint });
      }

      if (priceType === "BP" && user.basepoint < price) {
          return res.json({ success: true, sufficientFunds: false, cashpoint: user.cashpoint, basepoint: user.basepoint });
      }

      if (priceType === "CP") {
          user.cashpoint -= price;
      } else if (priceType === "BP") {
          user.basepoint -= price;
      }
      await user.save();

      const newItem = await Outfit.create({
          UserID: userID,
          Character_costume: characterNum,
          Type: type,
          Description: description
      });

      if (worn) {
          const character = await Character.findOne({ where: { UserID: userID, CharacterType: characterNum } });
          if (!character) {
              return res.status(404).json({ success: false, message: "Character not found" });
          }

          switch (type) {
              case 'Top':
                  await character.update({ TopOutfitID: newItem.OutfitID });
                  break;
              case 'Bottom':
                  await character.update({ BottomOutfitID: newItem.OutfitID });
                  break;
              case 'Shoes':
                  await character.update({ ShoesOutfitID: newItem.OutfitID });
                  break;
              default:
                  return res.status(400).json({ success: false, message: "Invalid outfit type" });
          }
      }

      res.json({ success: true, sufficientFunds: true, wornDone: true, cashpoint: user.cashpoint, basepoint: user.basepoint });
  } catch (error) {
      console.log(error);
      res.status(500).json({ success: false, message: "Error processing the purchase: " + error.message });
  }
});

app.post('/update-lootitem', async (req, res) => {
  const { userID, items } = req.body;

  try {
    await Promise.all(items.map(async item => {
      const { ItemName, ItemCount } = item;
      const existingItem = await LootItem.findOne({
        where: {
          Description: ItemName,
          UserID: userID
        }
      });

      if (existingItem) {
        return existingItem.increment('Number', { by: ItemCount });
      } else {
        return LootItem.create({
          Description: ItemName,
          Number: ItemCount,
          UserID: userID
        });
      }
    }));

    const updatedItems = await LootItem.findAll({
      where: {
        UserID: userID
      }
    });
    
    res.json({ lootItem: updatedItems });
  } catch (error) {
    console.error("Error updating loot items:", error);
    res.status(500).json({ success: false, message: "Failed to update loot items." });
  }
});

app.post('/update-game-stats', async (req, res) => {
  const { userID, addBasePoints, newExperience, newLevel } = req.body;

  try {
      const user = await User.findByPk(userID);
      if (!user) {
          return res.status(404).json({ success: false, message: "User not found" });
      }

      user.basepoint += addBasePoints;
      user.ExperiencePoints = newExperience;
      user.Level = newLevel;

      await user.save();

      res.json({
          success: true,
          newLevel: user.Level,
          newExperience: user.ExperiencePoints,
          newBp: user.basepoint
      });
  } catch (error) {
      console.error("Error updating user stats:", error);
      res.status(500).json({ success: false, message: "Failed to update user stats" });
  }
});

app.post('/purchase-item', async (req, res) => {
  const { steamID, itemPrice } = req.body;

  try {

    const user = await User.findOne({ where: { SteamID: steamID } });
    if (!user) {
      return res.status(404).json({ exists: false, message: "User not found" });
    }

    user.cashpoint += itemPrice;

    await user.save();

    res.json({ 
      exists: true, 
      cashpoint: user.cashpoint
    });
  } catch (error) {
    console.error("Error processing purchase:", error);
    res.status(500).json({ exists: false, message: "Failed to purchased" });
  }
});

app.listen(PORT, () => {
  console.log(`Server is running on http://localhost:${PORT}`);
});
